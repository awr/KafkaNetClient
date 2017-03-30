using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using KafkaClient.Common;
using KafkaClient.Connections;
using KafkaClient.Testing;
using NUnit.Framework;

namespace KafkaClient.Tests.Unit
{
    public class SslTransportTests : TransportTests<SslTransport>
    {
        private const string CertPassword = "password";
        private static readonly byte[] CertRawBytes = { 0x30, 0x82, 0xA, 0x2F, 0x2, 0x1, 0x3, 0x30, 0x82, 0x9, 0xEF, 0x6, 0x9, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0xD, 0x1, 0x7, 0x1, 0xA0, 0x82, 0x9, 0xE0, 0x4, 0x82, 0x9, 0xDC, 0x30, 0x82, 0x9, 0xD8, 0x30, 0x82, 0x6, 0xC, 0x6, 0x9, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0xD, 0x1, 0x7, 0x1, 0xA0, 0x82, 0x5, 0xFD, 0x4, 0x82, 0x5, 0xF9, 0x30, 0x82, 0x5, 0xF5, 0x30, 0x82, 0x5, 0xF1, 0x6, 0xB, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0xD, 0x1, 0xC, 0xA, 0x1, 0x2, 0xA0, 0x82, 0x4, 0xFE, 0x30, 0x82, 0x4, 0xFA, 0x30, 0x1C, 0x6, 0xA, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0xD, 0x1, 0xC, 0x1, 0x3, 0x30, 0xE, 0x4, 0x8, 0xF4, 0xC4, 0x6F, 0x1E, 0x6A, 0x49, 0x55, 0x1D, 0x2, 0x2, 0x7, 0xD0, 0x4, 0x82, 0x4, 0xD8, 0xDE, 0x4C, 0x69, 0xF3, 0x25, 0xA3, 0xD6, 0x13, 0x2A, 0xC5, 0xC5, 0x6B, 0xBF, 0xD0, 0xA0, 0xC, 0x1E, 0x47, 0x4F, 0x7B, 0x79, 0xE8, 0xF1, 0x18, 0xCD, 0x9, 0x7D, 0x1E, 0x61, 0x98, 0x79, 0xE9, 0x31, 0x81, 0x7A, 0x62, 0xD0, 0x17, 0x88, 0xA6, 0xAA, 0x81, 0x9E, 0x90, 0x4E, 0xA2, 0xAB, 0x46, 0x55, 0x4B, 0xB5, 0x3A, 0xBF, 0x59, 0x4B, 0xBA, 0x64, 0x8F, 0x5E, 0x38, 0x2E, 0xF9, 0x38, 0x87, 0x53, 0xBA, 0x23, 0x5C, 0x25, 0xEE, 0x37, 0x95, 0xC6, 0xF4, 0xAC, 0x65, 0xEA, 0x34, 0xFD, 0x8C, 0xF4, 0x81, 0xDC, 0x91, 0x33, 0x9C, 0xBF, 0xD5, 0xD, 0xB6, 0x3D, 0x80, 0x1B, 0x67, 0x99, 0x23, 0xAC, 0xA5, 0x10, 0xD9, 0x9D, 0xF2, 0x30, 0x7E, 0x2B, 0xD4, 0xAF, 0xF1, 0x24, 0xAF, 0x23, 0xD, 0x71, 0x9A, 0x4F, 0x85, 0x13, 0x54, 0xCB, 0xD9, 0x18, 0xE8, 0x9C, 0xAD, 0x9A, 0xA2, 0x1E, 0xD4, 0x6D, 0x1F, 0xB6, 0xD7, 0x75, 0xE3, 0x30, 0x21, 0xD3, 0x22, 0x8A, 0xC6, 0xFE, 0x1A, 0x23, 0xBA, 0xA5, 0xDF, 0xD5, 0x2A, 0xD, 0x8B, 0x97, 0x4A, 0xB6, 0xFF, 0x51, 0x25, 0x2C, 0xFE, 0x4F, 0x1C, 0xE4, 0xE0, 0x9E, 0x49, 0xC7, 0x9, 0x5E, 0x6B, 0x37, 0x9C, 0xC8, 0x3E, 0xC4, 0xC8, 0x8C, 0xCE, 0x49, 0x46, 0x1, 0x13, 0xBA, 0x8B, 0x1F, 0xBE, 0xD6, 0x95, 0x24, 0x8B, 0xD, 0xED, 0x4, 0xC, 0x57, 0x5F, 0x81, 0xD, 0x5C, 0xD8, 0xAB, 0x33, 0x26, 0x26, 0x73, 0x0, 0xA8, 0x69, 0xE1, 0x8C, 0x82, 0x22, 0x7F, 0x74, 0x38, 0x62, 0x80, 0x5E, 0x38, 0x53, 0x9F, 0xD6, 0x11, 0x28, 0xB5, 0x98, 0x1E, 0x55, 0xF9, 0x3, 0xEB, 0x4D, 0x5B, 0x2C, 0xE9, 0x53, 0x29, 0xD9, 0xF7, 0x5D, 0xD6, 0xAA, 0x77, 0x9F, 0xA0, 0x99, 0x4, 0x2E, 0x8C, 0x27, 0x42, 0x57, 0x19, 0x94, 0x52, 0x85, 0xB0, 0xD3, 0xAD, 0xD8, 0x34, 0x12, 0x50, 0xF2, 0x97, 0xA4, 0xD9, 0x5E, 0x50, 0xE1, 0x6D, 0xF3, 0xC0, 0xF8, 0x5F, 0x6F, 0xBC, 0xA5, 0x58, 0x2, 0xF4, 0x8A, 0xB0, 0xC9, 0x33, 0xE, 0xA2, 0x4D, 0x72, 0xED, 0x4D, 0xC, 0x7C, 0x53, 0xFE, 0xC5, 0x4B, 0xA5, 0x58, 0xD, 0xAA, 0x58, 0x3C, 0x4F, 0x9A, 0x7A, 0x22, 0x17, 0x80, 0x35, 0xF7, 0x93, 0xB6, 0xEA, 0xDC, 0x3A, 0x9E, 0xEE, 0xA0, 0x8F, 0xE0, 0x94, 0xFB, 0xA5, 0xDD, 0xB3, 0xF4, 0xE1, 0xA6, 0x6, 0x67, 0xB1, 0x9F, 0xC0, 0x1B, 0x3F, 0x49, 0x7D, 0x22, 0x55, 0x67, 0xC5, 0xBF, 0x21, 0xFD, 0x38, 0xA3, 0x13, 0xAD, 0xB7, 0xC, 0x7B, 0x4, 0x48, 0xC7, 0xF6, 0xFB, 0x26, 0xA8, 0x95, 0x76, 0x8A, 0x18, 0xC1, 0xF8, 0x86, 0xBF, 0xD8, 0x2C, 0xDD, 0x16, 0x0, 0x83, 0xD7, 0x27, 0x33, 0x31, 0x12, 0xA8, 0x76, 0x4, 0x88, 0xEF, 0x7D, 0x7F, 0x59, 0xA6, 0x4A, 0x9C, 0x7F, 0xB, 0x88, 0x3A, 0x4, 0xA, 0xBA, 0x60, 0xB1, 0xB6, 0xCB, 0xD3, 0x5D, 0xE1, 0xE0, 0x6F, 0x3, 0x6F, 0xB, 0x9A, 0x9D, 0xA1, 0x96, 0x34, 0xDF, 0xE5, 0x62, 0xEE, 0xE6, 0xB5, 0xA, 0xC5, 0x1E, 0x98, 0xC4, 0xF4, 0x62, 0xFE, 0xFF, 0x8D, 0x22, 0xD8, 0x5F, 0xBB, 0x30, 0x46, 0x0, 0xF9, 0x33, 0xA2, 0x37, 0x43, 0xE6, 0xAE, 0x51, 0xBE, 0x6B, 0xE1, 0x35, 0x62, 0xCF, 0x16, 0x18, 0x4E, 0x64, 0x1B, 0x2, 0x89, 0x88, 0xD, 0xC3, 0xCC, 0x8E, 0x8, 0x2F, 0xAA, 0x55, 0x93, 0xD3, 0x5D, 0xFC, 0x4F, 0xC2, 0x90, 0xA5, 0x17, 0xFC, 0x84, 0x95, 0xBF, 0xCB, 0xE2, 0x4, 0x6C, 0xFB, 0x45, 0xEF, 0xC3, 0x6C, 0x28, 0xDD, 0x70, 0xC5, 0x2C, 0x8, 0xD6, 0xD9, 0x47, 0x9E, 0xBF, 0x27, 0x98, 0x51, 0xAC, 0x7A, 0xCF, 0x6B, 0x63, 0xF4, 0x93, 0x5D, 0xB8, 0x5D, 0xE5, 0x72, 0x97, 0x6B, 0x16, 0x35, 0x24, 0xB8, 0x31, 0xD0, 0x6C, 0x68, 0xC3, 0x70, 0xC6, 0x65, 0x8F, 0x6D, 0xF8, 0x21, 0xCF, 0x7D, 0x36, 0x91, 0xC7, 0x5C, 0x75, 0x8C, 0xFA, 0x76, 0x49, 0xE9, 0xE9, 0x47, 0x9A, 0xCE, 0x64, 0xF8, 0x3F, 0xC6, 0x8A, 0x8B, 0xD4, 0x89, 0x26, 0x91, 0x69, 0x41, 0xF6, 0xD8, 0x94, 0x9B, 0xBB, 0x90, 0x39, 0xEA, 0xBB, 0x33, 0xCC, 0x13, 0x4F, 0x17, 0x2C, 0xA4, 0x19, 0xE7, 0xE5, 0xEB, 0xBC, 0xBF, 0x2E, 0x5D, 0xAB, 0xD4, 0xE3, 0xCE, 0xD8, 0x2B, 0x9C, 0x69, 0xB9, 0x1, 0x5D, 0x97, 0x91, 0x6F, 0xA2, 0x31, 0x9, 0x43, 0x43, 0xAF, 0xBF, 0x26, 0xF3, 0x93, 0x35, 0xAA, 0x76, 0x4F, 0x6D, 0x87, 0xCC, 0x21, 0x80, 0x7C, 0x34, 0x45, 0xF2, 0x97, 0xE3, 0x33, 0x8B, 0x6F, 0x1F, 0x76, 0xA2, 0xDA, 0x23, 0x3E, 0x5B, 0xCB, 0x71, 0x23, 0xD, 0x20, 0x49, 0xA8, 0x87, 0x7D, 0x22, 0x9E, 0x89, 0x66, 0x59, 0xEF, 0xAC, 0x74, 0xA7, 0xA2, 0x2D, 0x24, 0xC4, 0x22, 0xBC, 0x68, 0xBE, 0xFF, 0x31, 0xDC, 0xAC, 0x74, 0x9D, 0x79, 0x4D, 0x28, 0x6F, 0xA8, 0x41, 0x76, 0x86, 0xCE, 0xE8, 0xB0, 0x8A, 0xE0, 0x3D, 0x73, 0x46, 0x28, 0x30, 0x82, 0x36, 0x73, 0x40, 0x1B, 0xD, 0x4F, 0xDD, 0xA9, 0x6, 0xE6, 0xCC, 0x3E, 0x84, 0x58, 0x24, 0xDA, 0x31, 0x38, 0x83, 0x1C, 0x25, 0x7C, 0xAC, 0xC4, 0xF7, 0x34, 0x22, 0x4E, 0x3B, 0x3C, 0xC4, 0xB2, 0x7E, 0xBD, 0x18, 0x40, 0x43, 0x33, 0x54, 0x38, 0x1F, 0xDE, 0xE3, 0xF1, 0xAE, 0xC4, 0x6B, 0x6B, 0x8, 0x7E, 0x4, 0x91, 0x40, 0x86, 0xD3, 0xAF, 0x75, 0xC0, 0x94, 0x5F, 0x97, 0x6C, 0xFC, 0x7A, 0x32, 0xB3, 0x74, 0xFA, 0x52, 0x23, 0xDA, 0xBB, 0x68, 0x82, 0x3E, 0xE, 0x57, 0x7A, 0xDF, 0xC0, 0x2F, 0xD9, 0xBB, 0x1E, 0xB5, 0xE3, 0x5D, 0x9D, 0xC8, 0xE3, 0xD, 0x2, 0xE9, 0x21, 0x22, 0xB7, 0x30, 0x6, 0x27, 0xA8, 0xE3, 0x71, 0xA2, 0xF0, 0xBA, 0xEB, 0x4F, 0xB2, 0x62, 0x1A, 0xDF, 0x78, 0x48, 0xA9, 0x4C, 0xEE, 0x89, 0xEB, 0xFD, 0x3E, 0xC5, 0x31, 0x59, 0x8C, 0x17, 0x36, 0x7F, 0x46, 0xF4, 0xC4, 0xF6, 0x5E, 0x1F, 0x1, 0x9D, 0xAA, 0x4B, 0x74, 0x9D, 0xE3, 0xF4, 0x9, 0x3, 0xAE, 0x1A, 0xE5, 0x76, 0xD9, 0xD2, 0x92, 0x96, 0x8F, 0x34, 0x2E, 0x7B, 0x7D, 0xCF, 0xA4, 0x42, 0xDC, 0xE2, 0x3C, 0xE7, 0x55, 0x76, 0x80, 0xB7, 0xC9, 0x28, 0x95, 0xB5, 0x56, 0xC6, 0x7A, 0x8F, 0x5F, 0x4E, 0x44, 0x98, 0x27, 0x8F, 0xC, 0xF0, 0x5D, 0x28, 0x9D, 0xEB, 0x62, 0xE0, 0xF9, 0x66, 0x18, 0x56, 0x3F, 0x77, 0xD2, 0x57, 0xBC, 0x1C, 0x87, 0xB7, 0x38, 0xA7, 0x21, 0x86, 0xD, 0x6D, 0xDC, 0xA6, 0xA5, 0x6E, 0x7F, 0xD0, 0xA2, 0x6, 0xA7, 0xD1, 0xB6, 0x35, 0xB8, 0x28, 0x53, 0xB1, 0xA, 0x8F, 0x15, 0x1F, 0xBD, 0x97, 0x67, 0xDD, 0x47, 0x70, 0x5A, 0x47, 0xE1, 0x42, 0x11, 0x33, 0xAC, 0x2E, 0xFE, 0x6B, 0xC1, 0xA9, 0xB5, 0x73, 0x27, 0x33, 0xE1, 0x50, 0x38, 0xDB, 0xBD, 0x3C, 0xD2, 0x58, 0xFB, 0xC2, 0xD8, 0x58, 0x38, 0x42, 0x4B, 0x3E, 0x9D, 0x90, 0x50, 0x8E, 0x9E, 0xC9, 0x80, 0x37, 0xC7, 0xE4, 0xF3, 0xB8, 0xA2, 0xE4, 0x36, 0x31, 0xC8, 0x70, 0xDA, 0x32, 0x5E, 0x82, 0xA2, 0xFE, 0x71, 0xE4, 0x46, 0x7B, 0x68, 0x7C, 0x20, 0x2E, 0xC8, 0x6D, 0x14, 0xB6, 0x79, 0xCE, 0x84, 0xFA, 0x15, 0x7, 0xE0, 0x58, 0xF1, 0x44, 0x13, 0xDB, 0x44, 0xEA, 0x5F, 0xCA, 0x6E, 0xAC, 0x33, 0x8D, 0xF1, 0x27, 0xEF, 0x7D, 0x48, 0xD3, 0xF3, 0x11, 0xAE, 0x82, 0xAE, 0xAE, 0xAD, 0x17, 0xD4, 0x55, 0x95, 0x2D, 0xA0, 0x7F, 0x6E, 0x83, 0x24, 0xAB, 0x11, 0xE6, 0x5E, 0xFB, 0x0, 0xA8, 0x6B, 0x96, 0xC5, 0xF6, 0x4B, 0x67, 0xF7, 0xCC, 0xEC, 0x4A, 0xF6, 0x37, 0x48, 0x68, 0x7A, 0xEC, 0xE8, 0xFA, 0x8, 0x85, 0x39, 0xD1, 0x4B, 0x9B, 0x72, 0xE8, 0xE4, 0x7F, 0x62, 0xE5, 0xB, 0xBA, 0x1C, 0x8B, 0xFB, 0x4E, 0xAC, 0x24, 0xFD, 0xB1, 0x97, 0x24, 0x91, 0x62, 0x10, 0x57, 0xEE, 0x9F, 0x29, 0x4, 0xA0, 0xDA, 0x57, 0x59, 0xEA, 0x81, 0xCA, 0xB1, 0x69, 0x58, 0xEA, 0xEC, 0x58, 0x8, 0xE9, 0xE5, 0x85, 0x5C, 0xCB, 0xD9, 0x4F, 0x84, 0x7A, 0xF5, 0xCF, 0x7B, 0x88, 0xA3, 0x18, 0xF4, 0xF7, 0x9B, 0xC, 0xEA, 0xCE, 0xEF, 0x6, 0x10, 0xF2, 0x62, 0x73, 0xCA, 0x52, 0x6A, 0xA7, 0x70, 0xDB, 0x47, 0xCB, 0x1B, 0x27, 0x2D, 0x66, 0x0, 0xBD, 0x10, 0xD9, 0x19, 0x81, 0xBE, 0x44, 0x19, 0x13, 0x75, 0x9E, 0xA6, 0xE4, 0x4, 0x4D, 0x6E, 0x70, 0xC6, 0x91, 0x37, 0x71, 0x60, 0x5E, 0xD5, 0x35, 0x0, 0xF6, 0x86, 0xA2, 0x4D, 0x1B, 0x94, 0x6F, 0xB4, 0xD, 0x6E, 0x56, 0xAB, 0x82, 0x44, 0x42, 0x14, 0xEA, 0x15, 0x43, 0xCB, 0x4A, 0xF5, 0x81, 0x26, 0xB8, 0x22, 0xB4, 0x71, 0xF3, 0xB7, 0x7C, 0x74, 0x85, 0xE4, 0xC7, 0xBB, 0xCC, 0xBB, 0xD8, 0xD8, 0xD7, 0x1A, 0xE8, 0x61, 0x35, 0x1B, 0x6E, 0x53, 0x35, 0x79, 0x19, 0x52, 0x98, 0x4E, 0x4D, 0xF, 0xC6, 0x2F, 0x1, 0xC6, 0xF9, 0x8D, 0xDD, 0x62, 0x19, 0x80, 0xAF, 0x81, 0x8D, 0x9F, 0x2D, 0xEE, 0xF, 0xC9, 0xE2, 0x91, 0xD6, 0x31, 0x81, 0xDF, 0x30, 0x13, 0x6, 0x9, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0xD, 0x1, 0x9, 0x15, 0x31, 0x6, 0x4, 0x4, 0x1, 0x0, 0x0, 0x0, 0x30, 0x5B, 0x6, 0x9, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0xD, 0x1, 0x9, 0x14, 0x31, 0x4E, 0x1E, 0x4C, 0x0, 0x7B, 0x0, 0x43, 0x0, 0x46, 0x0, 0x42, 0x0, 0x44, 0x0, 0x38, 0x0, 0x38, 0x0, 0x31, 0x0, 0x43, 0x0, 0x2D, 0x0, 0x36, 0x0, 0x42, 0x0, 0x31, 0x0, 0x41, 0x0, 0x2D, 0x0, 0x34, 0x0, 0x32, 0x0, 0x35, 0x0, 0x35, 0x0, 0x2D, 0x0, 0x41, 0x0, 0x45, 0x0, 0x37, 0x0, 0x39, 0x0, 0x2D, 0x0, 0x35, 0x0, 0x42, 0x0, 0x36, 0x0, 0x33, 0x0, 0x35, 0x0, 0x37, 0x0, 0x37, 0x0, 0x34, 0x0, 0x30, 0x0, 0x37, 0x0, 0x38, 0x0, 0x37, 0x0, 0x7D, 0x30, 0x6B, 0x6, 0x9, 0x2B, 0x6, 0x1, 0x4, 0x1, 0x82, 0x37, 0x11, 0x1, 0x31, 0x5E, 0x1E, 0x5C, 0x0, 0x4D, 0x0, 0x69, 0x0, 0x63, 0x0, 0x72, 0x0, 0x6F, 0x0, 0x73, 0x0, 0x6F, 0x0, 0x66, 0x0, 0x74, 0x0, 0x20, 0x0, 0x45, 0x0, 0x6E, 0x0, 0x68, 0x0, 0x61, 0x0, 0x6E, 0x0, 0x63, 0x0, 0x65, 0x0, 0x64, 0x0, 0x20, 0x0, 0x43, 0x0, 0x72, 0x0, 0x79, 0x0, 0x70, 0x0, 0x74, 0x0, 0x6F, 0x0, 0x67, 0x0, 0x72, 0x0, 0x61, 0x0, 0x70, 0x0, 0x68, 0x0, 0x69, 0x0, 0x63, 0x0, 0x20, 0x0, 0x50, 0x0, 0x72, 0x0, 0x6F, 0x0, 0x76, 0x0, 0x69, 0x0, 0x64, 0x0, 0x65, 0x0, 0x72, 0x0, 0x20, 0x0, 0x76, 0x0, 0x31, 0x0, 0x2E, 0x0, 0x30, 0x30, 0x82, 0x3, 0xC4, 0x6, 0x9, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0xD, 0x1, 0x7, 0x1, 0xA0, 0x82, 0x3, 0xB5, 0x4, 0x82, 0x3, 0xB1, 0x30, 0x82, 0x3, 0xAD, 0x30, 0x82, 0x3, 0xA9, 0x6, 0xB, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0xD, 0x1, 0xC, 0xA, 0x1, 0x3, 0xA0, 0x82, 0x3, 0x81, 0x30, 0x82, 0x3, 0x7D, 0x6, 0xA, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0xD, 0x1, 0x9, 0x16, 0x1, 0xA0, 0x82, 0x3, 0x6D, 0x4, 0x82, 0x3, 0x69, 0x30, 0x82, 0x3, 0x65, 0x30, 0x82, 0x2, 0x4D, 0xA0, 0x3, 0x2, 0x1, 0x2, 0x2, 0x9, 0x0, 0xEE, 0xB7, 0x13, 0x2A, 0xEB, 0x33, 0x71, 0x9D, 0x30, 0xD, 0x6, 0x9, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0xD, 0x1, 0x1, 0xB, 0x5, 0x0, 0x30, 0x49, 0x31, 0xB, 0x30, 0x9, 0x6, 0x3, 0x55, 0x4, 0x6, 0x13, 0x2, 0x43, 0x41, 0x31, 0x10, 0x30, 0xE, 0x6, 0x3, 0x55, 0x4, 0x8, 0xC, 0x7, 0x4F, 0x6E, 0x74, 0x61, 0x72, 0x69, 0x6F, 0x31, 0x14, 0x30, 0x12, 0x6, 0x3, 0x55, 0x4, 0xA, 0xC, 0xB, 0x4B, 0x61, 0x66, 0x6B, 0x61, 0x43, 0x6C, 0x69, 0x65, 0x6E, 0x74, 0x31, 0x12, 0x30, 0x10, 0x6, 0x3, 0x55, 0x4, 0x3, 0xC, 0x9, 0x6C, 0x6F, 0x63, 0x61, 0x6C, 0x68, 0x6F, 0x73, 0x74, 0x30, 0x1E, 0x17, 0xD, 0x31, 0x37, 0x30, 0x32, 0x30, 0x32, 0x31, 0x35, 0x32, 0x37, 0x32, 0x33, 0x5A, 0x17, 0xD, 0x32, 0x37, 0x30, 0x31, 0x33, 0x31, 0x31, 0x35, 0x32, 0x37, 0x32, 0x33, 0x5A, 0x30, 0x49, 0x31, 0xB, 0x30, 0x9, 0x6, 0x3, 0x55, 0x4, 0x6, 0x13, 0x2, 0x43, 0x41, 0x31, 0x10, 0x30, 0xE, 0x6, 0x3, 0x55, 0x4, 0x8, 0xC, 0x7, 0x4F, 0x6E, 0x74, 0x61, 0x72, 0x69, 0x6F, 0x31, 0x14, 0x30, 0x12, 0x6, 0x3, 0x55, 0x4, 0xA, 0xC, 0xB, 0x4B, 0x61, 0x66, 0x6B, 0x61, 0x43, 0x6C, 0x69, 0x65, 0x6E, 0x74, 0x31, 0x12, 0x30, 0x10, 0x6, 0x3, 0x55, 0x4, 0x3, 0xC, 0x9, 0x6C, 0x6F, 0x63, 0x61, 0x6C, 0x68, 0x6F, 0x73, 0x74, 0x30, 0x82, 0x1, 0x22, 0x30, 0xD, 0x6, 0x9, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0xD, 0x1, 0x1, 0x1, 0x5, 0x0, 0x3, 0x82, 0x1, 0xF, 0x0, 0x30, 0x82, 0x1, 0xA, 0x2, 0x82, 0x1, 0x1, 0x0, 0xE8, 0x38, 0x62, 0x69, 0xB8, 0x29, 0x74, 0xCE, 0xEA, 0x5, 0x5, 0x5C, 0x21, 0xF1, 0x23, 0xCD, 0x64, 0x18, 0x93, 0x32, 0xD3, 0xB6, 0xE7, 0xEF, 0xC1, 0xE4, 0x11, 0x3A, 0x7D, 0x96, 0x65, 0xF5, 0x19, 0x56, 0x59, 0xED, 0xF7, 0x26, 0xBE, 0xA7, 0x10, 0x27, 0xCF, 0x2E, 0x28, 0x78, 0x76, 0x39, 0x16, 0xF5, 0xA8, 0x82, 0x2B, 0xEC, 0xDD, 0x26, 0xC9, 0x2E, 0xD1, 0x92, 0x76, 0x10, 0x3, 0xD8, 0xE1, 0xB4, 0xE9, 0xBA, 0x94, 0x8C, 0xBB, 0x22, 0xB1, 0xC2, 0xE3, 0xD1, 0x38, 0xAB, 0xAC, 0xCD, 0xC7, 0x24, 0xD3, 0xD1, 0xEC, 0x2B, 0x15, 0x9, 0x95, 0x55, 0x4, 0xA0, 0x92, 0x9E, 0x52, 0x73, 0x19, 0x5D, 0x77, 0x3B, 0x3A, 0x4E, 0x81, 0x5C, 0xC6, 0x7A, 0x58, 0xB3, 0xC7, 0x97, 0xF6, 0xBC, 0x55, 0xCE, 0x7, 0xE6, 0x1F, 0xD8, 0xC5, 0x97, 0x43, 0x58, 0x88, 0x8D, 0xB3, 0xA3, 0xDE, 0x6C, 0x2A, 0xFF, 0x8B, 0x6D, 0xB1, 0x42, 0x82, 0x31, 0x64, 0xC4, 0xCE, 0x9B, 0xCA, 0xEC, 0x74, 0x51, 0x7A, 0x8, 0x27, 0xBB, 0x28, 0x65, 0xF9, 0x77, 0x6B, 0xE3, 0xFE, 0xC9, 0x7D, 0xFD, 0x1A, 0x79, 0x92, 0x79, 0xCC, 0xD, 0xEB, 0x18, 0x71, 0xC7, 0x75, 0x19, 0x67, 0xF5, 0x81, 0x94, 0x97, 0x76, 0xEC, 0x2B, 0xAA, 0xC3, 0x92, 0x73, 0xB3, 0x45, 0x6F, 0xE, 0x58, 0x7E, 0x48, 0xF0, 0x5C, 0x8D, 0xF1, 0xB6, 0xED, 0x3D, 0x94, 0xC9, 0x50, 0x67, 0x3, 0x2F, 0x5A, 0x10, 0x9C, 0xA8, 0xFE, 0xC2, 0x6D, 0x1E, 0x3D, 0xCD, 0x9, 0x3B, 0x1A, 0xC, 0x47, 0x8C, 0x1B, 0x87, 0xCA, 0x87, 0x84, 0x48, 0x63, 0xE, 0xB8, 0xB0, 0xDA, 0xE0, 0x1A, 0x7, 0xA3, 0x1F, 0x62, 0x4, 0x67, 0x78, 0xA0, 0xB2, 0x4A, 0xE4, 0xA0, 0xDA, 0x60, 0x99, 0xFB, 0xF0, 0xF5, 0x50, 0x70, 0xF, 0x33, 0x14, 0xB6, 0x21, 0x2, 0x3, 0x1, 0x0, 0x1, 0xA3, 0x50, 0x30, 0x4E, 0x30, 0x1D, 0x6, 0x3, 0x55, 0x1D, 0xE, 0x4, 0x16, 0x4, 0x14, 0xA1, 0x4E, 0x33, 0xB3, 0x8D, 0xA8, 0x42, 0xFC, 0x5E, 0x76, 0xB8, 0x34, 0x25, 0xF1, 0x68, 0xCB, 0x4C, 0xC5, 0x35, 0x83, 0x30, 0x1F, 0x6, 0x3, 0x55, 0x1D, 0x23, 0x4, 0x18, 0x30, 0x16, 0x80, 0x14, 0xA1, 0x4E, 0x33, 0xB3, 0x8D, 0xA8, 0x42, 0xFC, 0x5E, 0x76, 0xB8, 0x34, 0x25, 0xF1, 0x68, 0xCB, 0x4C, 0xC5, 0x35, 0x83, 0x30, 0xC, 0x6, 0x3, 0x55, 0x1D, 0x13, 0x4, 0x5, 0x30, 0x3, 0x1, 0x1, 0xFF, 0x30, 0xD, 0x6, 0x9, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0xD, 0x1, 0x1, 0xB, 0x5, 0x0, 0x3, 0x82, 0x1, 0x1, 0x0, 0x45, 0x99, 0x22, 0x8C, 0xF2, 0xDA, 0x8F, 0x8E, 0xEE, 0x18, 0xA6, 0xD8, 0xE2, 0xCA, 0xA9, 0x44, 0x70, 0x9F, 0x35, 0xC8, 0xF6, 0x8D, 0x1E, 0xF6, 0xB8, 0x98, 0x2E, 0x96, 0xA1, 0x99, 0xFD, 0xF, 0xAB, 0x76, 0x8F, 0x3A, 0xED, 0x89, 0xD3, 0xA, 0x93, 0xFF, 0x68, 0x93, 0x12, 0x6D, 0x27, 0x5A, 0x47, 0x7F, 0xB6, 0x17, 0xC1, 0xBD, 0x23, 0x89, 0xB, 0xB8, 0x48, 0xD5, 0xD6, 0x66, 0xFD, 0xAD, 0x57, 0x5, 0xB, 0xB, 0x65, 0x58, 0xDB, 0xBD, 0x2A, 0x17, 0xB1, 0x49, 0xC3, 0x9D, 0xC7, 0x5F, 0x9D, 0x93, 0xBF, 0x70, 0x32, 0x6, 0x1, 0x64, 0x8F, 0x99, 0xE0, 0x6B, 0x1E, 0x83, 0xC6, 0x61, 0xE0, 0x30, 0xF4, 0xCD, 0x6A, 0x54, 0x14, 0x37, 0x59, 0x52, 0xD9, 0x27, 0x41, 0xCB, 0x97, 0xF5, 0x65, 0xB7, 0xC, 0x1B, 0x78, 0xF9, 0xC3, 0x8A, 0xBE, 0x66, 0xD8, 0x3C, 0x94, 0x7B, 0x8B, 0xB3, 0x31, 0x56, 0xF8, 0xDD, 0x73, 0x9E, 0x5D, 0x75, 0xB9, 0x25, 0xD6, 0x27, 0x36, 0x59, 0x32, 0xA0, 0x97, 0xE6, 0xC2, 0xAB, 0x56, 0xBF, 0x64, 0x91, 0xEE, 0x3A, 0xFD, 0x6D, 0x8, 0xA9, 0x2B, 0xDB, 0x6F, 0xB9, 0x26, 0x20, 0x3A, 0x9A, 0x52, 0x13, 0xD4, 0x80, 0xB1, 0x2D, 0xD9, 0x35, 0x10, 0x0, 0x76, 0x28, 0x30, 0xF9, 0xC7, 0xF5, 0xC3, 0x38, 0x74, 0x82, 0xEB, 0xD7, 0xDF, 0x79, 0x5A, 0xEA, 0xDD, 0xC3, 0x3E, 0x67, 0xBA, 0x9E, 0x64, 0x1D, 0xAC, 0x35, 0xB1, 0x97, 0x7C, 0xD9, 0x88, 0xC6, 0x67, 0x18, 0xC9, 0xBD, 0x20, 0x86, 0xFD, 0xB, 0xD4, 0xF5, 0x31, 0x5A, 0xF5, 0x5D, 0x89, 0x3D, 0x59, 0x70, 0xE1, 0x68, 0x66, 0xDB, 0xDC, 0xBE, 0x8D, 0xE2, 0xBC, 0xC3, 0xDD, 0xA8, 0x59, 0x1E, 0xFC, 0x1D, 0x74, 0xE0, 0x4F, 0x5A, 0xFA, 0x52, 0x98, 0xEF, 0xDC, 0xDD, 0x45, 0x54, 0xC9, 0x9, 0x31, 0x15, 0x30, 0x13, 0x6, 0x9, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0xD, 0x1, 0x9, 0x15, 0x31, 0x6, 0x4, 0x4, 0x1, 0x0, 0x0, 0x0, 0x30, 0x37, 0x30, 0x1F, 0x30, 0x7, 0x6, 0x5, 0x2B, 0xE, 0x3, 0x2, 0x1A, 0x4, 0x14, 0xFF, 0xE, 0x9D, 0xE2, 0xFA, 0xCC, 0xA8, 0x3F, 0x90, 0x9, 0xFE, 0xF, 0xAD, 0x1F, 0xF, 0xF9, 0x96, 0x59, 0x9, 0x2C, 0x4, 0x14, 0x4, 0xBA, 0xF4, 0x81, 0x92, 0x2A, 0x1, 0x14, 0x48, 0x8D, 0x77, 0xA2, 0xB1, 0x2A, 0x86, 0x5A, 0x8B, 0xF8, 0x4D, 0x19 };
        private readonly X509Certificate _certificate = new X509Certificate2(CertRawBytes, CertPassword);

        [Test]
        public void CreatingSslTransportWithoutSslConfigurationThrowsException()
        {
            var config = new ConnectionConfiguration(sslConfiguration: null);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => {
                    using (new SslTransport(TestConfig.ServerEndpoint(), config, TestConfig.Log)) { }
                });
        }

        //[Test]
        //[Ignore("Only for creating the certificate bytes from the file")]
        //public void CreateCertificate()
        //{
        //    // to recreate the cert, you want something like the following, run with root/admin level access (with common name = localhost):
        //    // openssl req -newkey rsa:2048 -nodes -keyout localhost.key -x509 -days 3650 -out localhost.crt
        //    // openssl pkcs12 -inkey localhost.key -in localhost.crt -export -out localhost.pfx

        //    var path = @"C:\Code\KafkaClient\src\KafkaClient.Tests\localhost.pfx";
        //    var cert = new X509Certificate2();
        //    cert.Import(path, CertPassword, X509KeyStorageFlags.Exportable);

        //    var buffer = new StringBuilder("private static readonly byte[] CertRawBytes = { ");
        //    foreach (var b in cert.Export(X509ContentType.Pfx, CertPassword))
        //    {
        //        buffer.AppendFormat("0x{0:X}, ", b);
        //    }
        //    buffer.Remove(buffer.Length - 2, 2);
        //    buffer.Append(" };");

        //    Console.WriteLine(buffer.ToString());
        //}

        protected override SslTransport CreateTransport(Endpoint endpoint, IConnectionConfiguration configuration, ILog log)
        {
            var sslConfiguration = new SslConfiguration((sender, certificate, chain, errors) => (errors | SslPolicyErrors.RemoteCertificateChainErrors) == SslPolicyErrors.RemoteCertificateChainErrors);
            return new SslTransport(endpoint, configuration.CopyWith(sslConfiguration: sslConfiguration), log);
        }

        protected override TcpServer CreateServer(int port, ILog log)
        {
            return new TcpServer(port, log, _certificate);
        }
    }
}